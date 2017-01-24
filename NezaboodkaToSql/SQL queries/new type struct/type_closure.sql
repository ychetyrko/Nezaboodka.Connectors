CREATE DATABASE IF NOT EXISTS nz_test_closure;

USE nz_test_closure;

CREATE TABLE `type`(
	`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != ''),
	`table_name` VARCHAR(64) NOT NULL UNIQUE CHECK(`table_name` != ''),
	`base_type_name` VARCHAR(128) COLLATE `utf8_general_ci` CHECK(`table_name` != '')
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;

CREATE TABLE `field` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_type_name` VARCHAR(128) NOT NULL CHECK(`owner_type_name` != ''),
	`owner_type_id` INT DEFAULT NULL,
	`name` VARCHAR(128) NOT NULL CHECK(`name` != ''),
	`col_name` VARCHAR(64) NOT NULL COLLATE `utf8_general_ci` CHECK(`col_name` != ''),
	`type_name` VARCHAR(64) NOT NULL CHECK(`type_name` != ''),
	`ref_type_id` INT DEFAULT NULL,
	`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
	`compare_options` ENUM
		(
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None',
	`back_ref_name` VARCHAR(128) DEFAULT NULL CHECK(`back_ref_name` != ''),
	`back_ref_id` INT DEFAULT NULL,
	
	FOREIGN KEY(`owner_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY(`ref_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE RESTRICT,
	
	FOREIGN KEY(`back_ref_id`)
		REFERENCES `field`(`id`)
		ON DELETE SET NULL
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;

CREATE TABLE `type_closure`(
	`ancestor` INT NOT NULL,
	`descendant` INT NOT NULL,
		`is_straight` BOOLEAN NOT NULL DEFAULT false,
		
		FOREIGN KEY(`ancestor`)
			REFERENCES `type`(`id`)
			ON DELETE CASCADE,
		FOREIGN KEY(`descendant`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,
	CONSTRAINT `uc_keys` UNIQUE (`ancestor`, `descendant`)
) ENGINE=`InnoDB`;

CREATE TABLE `db_key` (
	`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
	`real_type_id` INT NOT NULL,
	
	FOREIGN KEY (`real_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

CREATE TABLE `list` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_id` BIGINT(0) NOT NULL,
	`type_id` INT NOT NULL,
	
	FOREIGN KEY (`owner_id`)
		REFERENCES `db_key`(`sys_id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY (`type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

CREATE TABLE `list_item` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`list_id` INT NOT NULL,
	`ref` BIGINT(0) NOT NULL,
	
	FOREIGN KEY (`list_id`)
		REFERENCES `list`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY (`ref`)
		REFERENCES `db_key`(`sys_id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

#--------------------------------------------------

delimiter //
drop procedure if exists before_alter_types //
create procedure before_alter_types()
begin
	CREATE TEMPORARY TABLE IF NOT EXISTS `type_add_list`(
		`name` VARCHAR(128) NOT NULL UNIQUE,
		`table_name` VARCHAR(64) NOT NULL UNIQUE,
		`base_type_name` VARCHAR(128)
	) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;

	truncate table `type_add_list`;
		
# ---> type_rem_list
end //

delimiter //
drop procedure if exists add_all_types //
create procedure add_all_types()
begin
	declare t_name varchar(128) default null;
		
		declare done boolean default false;
		declare cur cursor for
			select `name`
			from `nz_test_closure`.`type_add_list`;
		declare continue handler for not found
			set done = true;
		
		create temporary table if not exists `new_type`(
			`id` int not null,
			foreign key (`id`)
				references `type`(`id`)
				on delete cascade
		) engine=`MEMORY`;
		
		truncate table `new_type`;
		
		open cur;

		fetch cur
		into t_name;
		while not done do
			call add_type(t_name);
			
			fetch cur
			into t_name;
		end while;
		
		close cur;
		
		call process_all_new_types();

		drop table `type_add_list`;
end //

delimiter //
drop procedure if exists add_type //
create procedure add_type(in type_name VARCHAR(128))
begin
	declare tbl_name varchar(64) default null;
	declare base_name varchar(128) default null;
	declare base_id int default null;
	declare type_id int default null;
	
	select `table_name`, `base_type_name`
	into tbl_name, base_name
	from `nz_test_closure`.`type_add_list` as t
	where t.`name` = type_name
	limit 1;
	
	select `id`
	into base_id
	from `type` as t
	where t.`name` = base_name
	limit 1;

	insert into `type`
	(`name`, `base_type_name`, `table_name`)
	value
	(type_name, base_name, tbl_name);
	
	SELECT last_insert_id()
	into type_id;

	insert into `type_closure`
	(`ancestor`, `descendant`)
	select clos.`ancestor`, type_id
	from `type_closure` as clos
	where clos.`descendant` = base_id
	union
	select type_id, type_id;
		
	insert into `new_type` (`id`)
	value (type_id);
		
	delete from `type_add_list`
	where `name` = type_name;
end //

delimiter //
drop procedure if exists process_all_new_types //
create procedure process_all_new_types()
begin
	declare db_name varchar(64) default 'nz_test_closure';

	declare type_id int default null;
	declare t_type_name varchar(128) default null;
	declare t_table_name varchar(64) default null;

	declare cf_col_name VARCHAR(64) default null;
	declare cf_type_name varchar(128) default null;
	declare cf_ref_type_id int default null;
	declare cf_is_list boolean default false;
	declare cf_compare_options varchar(64);
	declare nullable_sign_pos int default 0;

	declare types_done boolean default false;
	declare new_type_cur cursor for
		select t.`id`, t.`name` ,t.`table_name`
		from `nz_test_closure`.`type` as t
		join `nz_test_closure`.`new_type` as n
		where t.`id` = n.`id`;
	declare continue handler for not found
		set types_done = true;
	
	open new_type_cur;

	fetch new_type_cur	
	into type_id, t_type_name, t_table_name;
	while not types_done do
# ----> Process new type <----
	begin
		declare fields_defs TEXT default '';
		declare fields_constraints TEXT default '';
		declare field_type varchar(128);

		declare fields_done boolean default false;
		declare fields_cur cursor for
			select `col_name`, `type_name`, `ref_type_id`, `is_list`, `compare_options`
			from `nz_test_closure`.`field` as f
			where f.`owner_type_id` in (
				select clos.`ancestor`
				from `nz_test_closure`.`type_closure` as clos
				where clos.`descendant` = type_id
			);
		declare continue handler for not found
			set fields_done = true;
		
		open fields_cur;

		fetch fields_cur
		into cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options;
		while not fields_done do
			IF cf_ref_type_id IS NULL THEN
				IF NOT cf_is_list THEN
					SET field_type = cf_type_name;
					
					IF field_type LIKE 'VARCHAR(%' OR field_type = 'TEXT' THEN
						# --> Compare Options
						IF cf_compare_options = 'IgnoreCase' THEN
							SET field_type = CONCAT(field_type, ' COLLATE `utf8_general_ci`');
						END IF;
					ELSE
						# --> Check if nullable
						SELECT LOCATE('?', field_type)
						INTO nullable_sign_pos;
						
						IF nullable_sign_pos = 0 THEN
							SET field_type = CONCAT(field_type, ' NOT NULL');
						ELSE
							SET field_type = SUBSTRING(field_type FROM 1 FOR nullable_sign_pos-1);
						END IF;
					END IF;
					
				ELSE	# <-- @cf_is_list == TRUE
					SET field_type = 'BLOB';
				END IF;
				
			ELSE	# <-- @cf_ref_type_id != NULL
				IF NOT cf_is_list THEN
					SET field_type = 'BIGINT(0)';
					
					SET fields_constraints = CONCAT(fields_constraints, ',
						FOREIGN KEY (', @cf_col_name,')
							REFERENCES `db_key`(`sys_id`)
							ON DELETE SET NULL
							ON UPDATE SET NULL');
					
				ELSE	# <-- @cf_is_list == TRUE
					SET field_type = 'INT';
					SET fields_constraints = CONCAT(fields_constraints, ',
						FOREIGN KEY (', cf_col_name,')
							REFERENCES `list`(`id`)
							ON DELETE SET NULL');
				END IF;
			END IF;
			
			SET fields_defs = CONCAT(fields_defs, ', `', cf_col_name, '` ', field_type);
			
			fetch fields_cur
			into cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options;
		end while;

		# Create table for type with all ancestors' fields
		#  (table name can't be a parameter => prepare each time)
		SET @prep_str = CONCAT('
			CREATE TABLE `', db_name ,'`.`', t_table_name, '` (
				id BIGINT(0) PRIMARY KEY NOT NULL

				', fields_defs ,',

				FOREIGN KEY (id)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE CASCADE

					', fields_constraints, '

			) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;
		');

		select @prep_str;

		PREPARE p_create_table FROM @prep_str;
		EXECUTE p_create_table;
		DEALLOCATE PREPARE p_create_table;
	end;

		fetch new_type_cur
		into type_id, t_type_name, t_table_name;
	end while;
	
	close new_type_cur;
end //

#--------------------------------------------------

delimiter //
drop procedure if exists get_fields_constraints //
create procedure (in type_name varchar(128), in is_altering boolean, out field_defs TEXT, out field_constraints TEXT)
begin
	
end //
#--------------------------------------------------

delimiter //
drop procedure if exists remove_type //
create procedure remove_type(in type_name varchar(128))
begin
	declare desc_count int default null;
		declare type_id int default null;

		select `id`
		into type_id
		from `type`
		where `type`.`name` = type_name
		limit 1;

	# Сheck if terminating type
	select count(clos.`ancestor`)
		into desc_count
		from `type_closure` as clos
		where clos.`ancestor` = type_id;
		
		if (desc_count = 1) then
			delete from `type`
			where `type`.`id` = type_id;
		else
			signal sqlstate '40000'
				set message_text = "Can't insert type to the center of hierarchy";
		end if;
end //

#--------------------------------------------------

delimiter //
drop procedure if exists before_alter_fields //
create procedure before_alter_fields()
begin
	CREATE TEMPORARY TABLE IF NOT EXISTS `field_add_list`(
		`owner_type_name` VARCHAR(128) NOT NULL check(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL check(`name` != ''),
		`col_name` VARCHAR(64) NOT NULL COLLATE `utf8_general_ci` check(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL check(`type_name` != ''),
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM
			(
				'None',
				'IgnoreCase',
				'IgnoreNonSpace',
				'IgnoreSymbols',
				'IgnoreKanaType',
				'IgnoreWidth',
				'OrdinalIgnoreCase',
				'StringSort',
				'Ordinal'
			) NOT NULL DEFAULT 'None',
		`back_ref_name` VARCHAR(128) DEFAULT NULL check(`back_ref_name` != '')
	) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;

	truncate table `field_add_list`;
		
# ---> fields_rem_list
end //

delimiter //
drop procedure if exists add_all_fields //
create procedure add_all_fields()
begin
	insert into `field`
	(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`, `owner_type_id`, `ref_type_id`)
	select newf.`name`, newf.`col_name`, newf.`owner_type_name`, newf.`type_name`, newf.`is_list`, newf.`compare_options`, newf.`back_ref_name`, ownt.`id`, reft.`id`
	from `field_add_list` as newf
	join `type` as ownt
	on ownt.`name` = newf.`owner_type_name`
	join `type` as reft
	on reft.`name` = newf.`type_name`;

	update `field` as f1
	join `field` as f2
	on f2.`name` = f1.`back_ref_name`
	set f1.`back_ref_id` = f2.`id`;

# ---> alter type tables

	truncate table `field_add_list`;
end //

#--------------------------------------------------

delimiter //
drop procedure if exists get_all_ancestors//
create procedure get_all_ancestors(id INT)
begin
	select t.name
	from type as t
	join type_closure as clos
	on t.id = clos.ancestor
	where clos.descendant = id
		and t.id != id;	# filter itself
end //

delimiter //
drop procedure if exists get_all_descendants//
create procedure get_all_descendants(id INT)
begin
	select t.name
	from type as t
	join type_closure as clos
	on t.id = clos.descendant
	where clos.ancestor = id
		and t.id != id;	# filter itself
end //

delimiter ;
