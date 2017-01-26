CREATE DATABASE IF NOT EXISTS `nz_test_closure`;

USE `nz_test_closure`;

CREATE TABLE `nz_test_closure`.`type`(
	`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != ''),
	`table_name` VARCHAR(64) NOT NULL UNIQUE CHECK(`table_name` != ''),
	`base_type_name` VARCHAR(128) COLLATE `utf8_general_ci` CHECK(`base_type_name` != '')
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;

CREATE TABLE `nz_test_closure`.`field` (
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
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY(`ref_type_id`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE RESTRICT,
	
	FOREIGN KEY(`back_ref_id`)
		REFERENCES `nz_test_closure`.`field`(`id`)
		ON DELETE SET NULL
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;

CREATE TABLE `nz_test_closure`.`type_closure`(
	`ancestor` INT NOT NULL,
	`descendant` INT NOT NULL,
		`is_straight` BOOLEAN NOT NULL DEFAULT false,
		
		FOREIGN KEY(`ancestor`)
			REFERENCES `nz_test_closure`.`type`(`id`)
			ON DELETE CASCADE,
		FOREIGN KEY(`descendant`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE,
	CONSTRAINT `uc_keys` UNIQUE (`ancestor`, `descendant`)
) ENGINE=`InnoDB`;

CREATE TABLE `nz_test_closure`.`db_key` (
	`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
	`real_type_id` INT NOT NULL,
	
	FOREIGN KEY (`real_type_id`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

CREATE TABLE `nz_test_closure`.`list` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`owner_id` BIGINT(0) NOT NULL,
	`type_id` INT NOT NULL,
	
	FOREIGN KEY (`owner_id`)
		REFERENCES `nz_test_closure`.`db_key`(`sys_id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY (`type_id`)
		REFERENCES `nz_test_closure`.`type`(`id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

CREATE TABLE `nz_test_closure`.`list_item` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`list_id` INT NOT NULL,
	`ref` BIGINT(0) NOT NULL,
	
	FOREIGN KEY (`list_id`)
		REFERENCES `nz_test_closure`.`list`(`id`)
		ON DELETE CASCADE,
	
	FOREIGN KEY (`ref`)
		REFERENCES `nz_test_closure`.`db_key`(`sys_id`)
		ON DELETE CASCADE
) ENGINE=`InnoDB`;

#--------------------------------------------------

delimiter //
drop procedure if exists before_alter_types //
create procedure before_alter_types()
begin
	DROP TABLE IF EXISTS `nz_test_closure`.`type_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`type_add_list`(
		`id` INT PRIMARY KEY NOT NULL UNIQUE AUTO_INCREMENT,
		`name` VARCHAR(128) NOT NULL UNIQUE CHECK(`name` != ''),
		`table_name` VARCHAR(64) NOT NULL UNIQUE CHECK(`table_name` != ''),
		`base_type_name` VARCHAR(128) CHECK(`table_name` != '')
	) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;
	
# ---> type_rem_list
end //

delimiter //
drop procedure if exists add_all_types //
create procedure add_all_types()
begin
	declare current_ord int default 0;
    
    drop table if exists `nz_test_closure`.`type_add_queue`;
    create temporary table `nz_test_closure`.`type_add_queue`(
		`ord` int not null,
		`id` int not null unique,	# to ignore already inserted elements
		foreign key (`id`)
			references `nz_test_closure`.`type_add_list`(`id`)
			on delete cascade
	) engine=`MEMORY`;
    
    # As TEMPORARY table can't be referred to multiple times in the same query:
    # https://dev.mysql.com/doc/refman/5.7/en/temporary-table-problems.html
    drop table if exists `nz_test_closure`.`type_temp_queue_buf`;
    create temporary table `nz_test_closure`.`type_temp_queue_buf`(
		`id` int not null,
		foreign key (`id`)
			references `nz_test_closure`.`type_add_list`(`id`)
			on delete cascade
	) engine=`MEMORY`;
    # Same reason
    drop table if exists `nz_test_closure`.`type_inserted_list_buf`;
    create temporary table `nz_test_closure`.`type_inserted_list_buf`(
		`id` int not null unique,
        `name` varchar(128) not null,
		foreign key (`id`)
			references `nz_test_closure`.`type_add_list`(`id`)
			on delete cascade
	) engine=`MEMORY`;
	
    # First: insert types with NO parents (roots)
    set current_ord = 0;
    insert into `nz_test_closure`.`type_add_queue`
	(`ord`, `id`)
	select current_ord, tadd.`id`
	from `nz_test_closure`.`type_add_list` as tadd
	where tadd.`base_type_name` is NULL;
    
    # Second: insert types with EXISTING parents
    set current_ord = current_ord + 1;
    insert into `nz_test_closure`.`type_add_queue`
	(`ord`, `id`)
	select current_ord, tadd.`id`
	from `nz_test_closure`.`type_add_list` as tadd
	join `nz_test_closure`.`type` as t
	on tadd.`base_type_name` = t.`name`;
	
    # Third: insert types with parents just inserted (main ordering loop)
    order_loop: LOOP
		delete from `nz_test_closure`.`type_temp_queue_buf`;
        delete from `nz_test_closure`.`type_inserted_list_buf`;
        set current_ord = current_ord + 1;
        
        insert into `nz_test_closure`.`type_inserted_list_buf`
        (`id`, `name`)
        select tadd.`id`, tadd.`name`
        from `nz_test_closure`.`type_add_list` as tadd
        join `nz_test_closure`.`type_add_queue` as q
        on tadd.`id` = q.`id`;
        
        insert into `nz_test_closure`.`type_temp_queue_buf`
        (`id`)
        select tadd.`id`
        from `nz_test_closure`.`type_add_list` as tadd
        join `nz_test_closure`.`type_inserted_list_buf` as insbuf
        on tadd.`base_type_name` = insbuf.`name`;
        
        insert ignore into `nz_test_closure`.`type_add_queue`
        (`ord`, `id`)
        select current_ord, b.`id`
		from `nz_test_closure`.`type_temp_queue_buf` as b;
        
        if (ROW_COUNT() = 0) then
			leave order_loop;
        end if;
    END LOOP;
    
    delete from `nz_test_closure`.`type_inserted_list_buf`;
    drop table `nz_test_closure`.`type_inserted_list_buf`;
    
    delete from `nz_test_closure`.`type_temp_queue_buf`;
    drop table `nz_test_closure`.`type_temp_queue_buf`;

# Debug
    select q.`ord`, tadd.`name`, tadd.`base_type_name`
    from `nz_test_closure`.`type_add_list` as tadd
    join `nz_test_closure`.`type_add_queue` as q
    on tadd.`id` = q.`id`
    order by q.`ord`;
    
# ------------------------------------------------------------------------
    drop table if exists `nz_test_closure`.`new_type`;
    create temporary table `nz_test_closure`.`new_type`(
		`id` int not null,
		foreign key (`id`)
			references `nz_test_closure`.`type`(`id`)
			on delete cascade
	) engine=`MEMORY`;
    
	begin
		declare t_name varchar(128) default null;
		
		declare done boolean default false;
		declare cur cursor for
			select tadd.`name`
			from `nz_test_closure`.`type_add_list` as tadd
            join `nz_test_closure`.`type_add_queue` as q
            on tadd.`id` = q.`id`
            order by q.`ord`;
		declare continue handler for not found
			set done = true;
		open cur;

		fetch cur
		into t_name;
		while not done do
			call add_type(t_name);
			
			fetch cur
			into t_name;
		end while;
		
		close cur;
	end;
    
    delete from `nz_test_closure`.`type_add_queue`;
    drop table `nz_test_closure`.`type_add_queue`;
    
# Process all new types
	begin
		declare db_name varchar(64) default 'nz_test_closure';

		declare t_type_name varchar(128) default null;
		declare t_table_name varchar(64) default null;
		declare fields_defs text;
		declare fields_constraints text;

		declare types_done boolean default false;
		declare new_type_cur cursor for
			select t.`name` ,t.`table_name`
			from `nz_test_closure`.`type` as t
			join `nz_test_closure`.`new_type` as n
			where t.`id` = n.`id`;
		declare continue handler for not found
			set types_done = true;
		
		open new_type_cur;

		fetch new_type_cur	
		into t_type_name, t_table_name;
		while not types_done do

			call get_type_fields_and_constraints(t_type_name, TRUE, fields_defs, fields_constraints);

			if (CHAR_LENGTH(fields_defs) > 0) then 
				set fields_defs = CONCAT(',', fields_defs);
			end if;

			if (CHAR_LENGTH(fields_constraints) > 0) then 
				set fields_constraints = CONCAT(',', fields_constraints);
			end if;

			# Create table for type with all ancestors' fields
			#  (table name can't be a parameter => prepare each time)
			SET @prep_str = CONCAT('
				CREATE TABLE `', db_name ,'`.`', t_table_name, '` (
					id BIGINT(0) PRIMARY KEY NOT NULL

					', fields_defs ,',

					FOREIGN KEY (id)
						REFERENCES `', db_name ,'`.`db_key`(`sys_id`)
						ON DELETE CASCADE

						', fields_constraints, '

				) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;
			');

		#select @prep_str;

			PREPARE p_create_table FROM @prep_str;
			EXECUTE p_create_table;
			DEALLOCATE PREPARE p_create_table;

			fetch new_type_cur
			into t_type_name, t_table_name;
		end while;
		
		close new_type_cur;
	end;

	drop table `nz_test_closure`.`new_type`;
	truncate table `nz_test_closure`.`type_add_list`;
end //

delimiter //
drop procedure if exists add_type //
create procedure add_type(in type_name VARCHAR(128))
begin
	declare tbl_name varchar(64) default null;
	declare base_name varchar(128) default null;
	declare base_id int default null;
	declare type_id int default null;
    
	select tadd.`table_name`, tadd.`base_type_name`
	into tbl_name, base_name
	from `nz_test_closure`.`type_add_list` as tadd
	where tadd.`name` = type_name
	limit 1;
	
	select t.`id`
	into base_id
	from `nz_test_closure`.`type` as t
	where t.`name` = base_name
	limit 1;

	insert into `nz_test_closure`.`type`
	(`name`, `base_type_name`, `table_name`)
	value
	(type_name, base_name, tbl_name);
	
	SELECT last_insert_id()
	into type_id;

	insert into `nz_test_closure`.`type_closure`
	(`ancestor`, `descendant`)
	select clos.`ancestor`, type_id
	from `nz_test_closure`.`type_closure` as clos
	where clos.`descendant` = base_id
	union
	select type_id, type_id;
		
	insert into `nz_test_closure`.`new_type` (`id`)
	value (type_id);
		
	delete from `nz_test_closure`.`type_add_list`
	where `name` = type_name;
end //

#--------------------------------------------------

delimiter //
drop procedure if exists get_type_fields_and_constraints //
create procedure get_type_fields_and_constraints
	(in c_type_name varchar(128), in inheriting boolean, out fields_defs TEXT, out fields_constraints TEXT)
begin
	declare c_type_id int default null;
	
	declare cf_id int default null;	# for constraints names
	declare cf_col_name VARCHAR(64) default null;
	declare cf_type_name varchar(128) default null;
	declare cf_ref_type_id int default null;
	declare cf_is_list boolean default false;
	declare cf_compare_options varchar(64);

	set fields_defs = '';
	set fields_constraints = '';

	select `id`
	into c_type_id
	from `nz_test_closure`.`type` as t
	where t.`name` = c_type_name;

	#select concat('Start ', c_type_name, '(', c_type_id,')', ' altering.') as debug;

	if inheriting then
	begin	# get all parents' fields
		declare fields_done boolean default false;
		declare fields_cur cursor for
			select f.`id`, f.`col_name`, f.`type_name`, f.`ref_type_id`, f.`is_list`, f.`compare_options`
			from `nz_test_closure`.`field` as f
			where f.`owner_type_id` in (
				select clos.`ancestor`
				from `nz_test_closure`.`type_closure` as clos
				where clos.`descendant` = c_type_id
			);
		declare continue handler for not found
			set fields_done = true;

		open fields_cur;

		fetch fields_cur
		into cf_id, cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options;
		while not fields_done do

			call update_fields_def_constr(fields_defs, fields_constraints, inheriting,
				c_type_id, cf_id, cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options);
			
			fetch fields_cur
			into cf_id, cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options;
		end while;
	end;
	else	# NOT inheriting
	begin	# get only NEW fields
		declare fields_done boolean default false;
		declare fields_cur cursor for
			select f.`id`, f.`col_name`, f.`type_name`, f.`ref_type_id`, f.`is_list`, f.`compare_options`
			from `nz_test_closure`.`new_field` as newf
			left join `nz_test_closure`.`field` as f
			on f.`id` = newf.`id`
			where f.`owner_type_id` in (
				select clos.`ancestor`
				from `nz_test_closure`.`type_closure` as clos
				where clos.`descendant` = c_type_id
			);
		declare continue handler for not found
			set fields_done = true;

		open fields_cur;

		fetch fields_cur
		into cf_id, cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options;
		while not fields_done do

			call update_fields_def_constr(fields_defs, fields_constraints, inheriting,
				c_type_id, cf_id, cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options);
			
			fetch fields_cur
			into cf_id, cf_col_name, cf_type_name, cf_ref_type_id, cf_is_list, cf_compare_options;
		end while;
	end;
	end if;

	if (left(fields_defs, 1) = ',') then
		set fields_defs = SUBSTRING(fields_defs, 2);
	end if;

	if (left(fields_constraints, 1) = ',') then
		set fields_constraints = SUBSTRING(fields_constraints, 2);
	end if;

	#select fields_defs;
	#select fields_constraints;
	#select concat('End ', c_type_name, '(', c_type_id,')', ' altering.') as debug;
end //
#--------------------------------------------------

delimiter //
drop procedure if exists update_fields_def_constr //
create procedure update_fields_def_constr (inout f_defs text, inout f_constrs text, in inheriting boolean,
	in c_type_id int, in cf_id int, in cf_col_name varchar(64), in cf_type_name varchar(128),
	in cf_ref_type_id int, in cf_is_list boolean, in cf_compare_options varchar(128))
begin
	declare constr_add_prefix text default 'CONSTRAINT FK_';
	declare constr_add_prefix_full text default '';
	declare field_type varchar(128);
	declare nullable_sign_pos int default 0;

	IF NOT inheriting THEN
		set constr_add_prefix = CONCAT('ADD ', constr_add_prefix);
	end if;

	#select concat(cf_col_name, ' 1') as debug;

	IF cf_ref_type_id IS NULL THEN

	#select concat(cf_col_name, ' is value-type') as debug;

		IF NOT cf_is_list THEN

	#select concat(cf_col_name, ' is not list') as debug;

			SET field_type = cf_type_name;
			
			IF field_type LIKE 'VARCHAR(%' OR field_type = 'TEXT' THEN

	#select concat(cf_col_name, ' is string') as debug;

				# --> Compare Options
				IF cf_compare_options = 'IgnoreCase' THEN
					SET field_type = CONCAT(field_type, ' COLLATE `utf8_general_ci`');
				END IF;
			ELSE

	#select concat(cf_col_name, ' is not string') as debug;

				# --> Check if nullable
				SELECT LOCATE('?', field_type)
				INTO nullable_sign_pos;
				
				IF nullable_sign_pos = 0 THEN
					SET field_type = CONCAT(field_type, ' NOT NULL');
				ELSE
					SET field_type = SUBSTRING(field_type FROM 1 FOR nullable_sign_pos-1);
				END IF;
			END IF;
			
		ELSE	# <-- cf_is_list == TRUE

	#select concat(cf_col_name, ' is list') as debug;

			SET field_type = 'BLOB';
		END IF;

	ELSE	# <-- cf_ref_type_id != NULL
	#select concat(cf_col_name, ' is reference') as debug;

		SET constr_add_prefix_full = CONCAT('
			',constr_add_prefix, c_type_id, '_', cf_id);
	#select concat('Constraint full prefix: ', constr_add_prefix_full) as debug;

	#select f_constrs as debug_constr_before_add;
		IF NOT cf_is_list THEN
			SET field_type = 'BIGINT(0)';

			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE SET NULL
					ON UPDATE SET NULL');
		ELSE	# <-- cf_is_list == TRUE
			SET field_type = 'INT';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `list`(`id`)
					ON DELETE SET NULL');
		END IF;
	#select f_constrs as debug_constr_after_add;
	END IF;
	
	SET f_defs = CONCAT(f_defs, ', `', cf_col_name, '` ', field_type);
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
		from `nz_test_closure`.`type` as t
		where t.`name` = type_name
		limit 1;

	# Сheck if terminating type
	select count(clos.`ancestor`)
		into desc_count
		from `nz_test_closure`.`type_closure` as clos
		where clos.`ancestor` = type_id;
		
		if (desc_count = 1) then
			delete from `nz_test_closure`.`type`
			where `id` = type_id;
# TODO: drop table
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
	DROP TABLE IF EXISTS `nz_test_closure`.`field_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`field_add_list`(
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

# ---> fields_rem_list
end //

delimiter //
drop procedure if exists add_all_fields //
create procedure add_all_fields()
begin
	insert into `nz_test_closure`.`field`
	(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`, `owner_type_id`, `ref_type_id`)
	select newf.`name`, newf.`col_name`, newf.`owner_type_name`, newf.`type_name`, newf.`is_list`, newf.`compare_options`, newf.`back_ref_name`, ownt.`id`, reft.`id`
	from `nz_test_closure`.`field_add_list` as newf
	join `nz_test_closure`.`type` as ownt
	on ownt.`name` = newf.`owner_type_name`
	left join `nz_test_closure`.`type` as reft
	on reft.`name` = newf.`type_name`;

	update `nz_test_closure`.`field` as f1
	join `nz_test_closure`.`field` as f2
	on f2.`name` = f1.`back_ref_name`
	set f1.`back_ref_id` = f2.`id`;

	drop table if exists `nz_test_closure`.`new_field`;
	create temporary table if not exists `nz_test_closure`.`new_field`(
		`id` int not null,
		foreign key (`id`)
			references `nz_test_closure`.`field`(`id`)
			on delete cascade
	) engine=`MEMORY`;
	
	insert into `nz_test_closure`.`new_field`
	select f.`id`
	from `nz_test_closure`.`field` as f
	join `nz_test_closure`.`field_add_list` as newf
	on f.`name` = newf.`name`
		and f.`owner_type_name` = newf.`owner_type_name`;

#TODO: autofill BackReferences

	# Update all types with new fields
	begin
		declare db_name varchar(64) default 'nz_test_closure';

		declare t_type_name varchar(128) default null;
		declare t_table_name varchar(64) default null;
		declare fields_defs text;
		declare fields_constraints text;

		declare types_done boolean default false;
		declare type_cur cursor for
			select t.`name` ,t.`table_name`
			from `nz_test_closure`.`type` as t;
		declare continue handler for not found
			set types_done = true;
		
		open type_cur;

		fetch type_cur	
		into t_type_name, t_table_name;
		while not types_done do

			call get_type_fields_and_constraints(t_type_name, FALSE, fields_defs, fields_constraints);

			if (LENGTH(fields_defs) > 0) then
            
				if (LENGTH(fields_constraints) > 0) then 
					set fields_constraints = CONCAT(',', fields_constraints);
				end if;
                
				SET @prep_str = CONCAT('
					ALTER TABLE `', db_name ,'`.`', t_table_name, '`
						ADD COLUMN (', fields_defs ,')
						', fields_constraints, ';
					');

			#select @prep_str as 'Altering query';

				PREPARE p_alter_table FROM @prep_str;
				EXECUTE p_alter_table;
				DEALLOCATE PREPARE p_alter_table;
			end if;

			fetch type_cur
			into t_type_name, t_table_name;
		end while;
		
		close type_cur;
	end;

	drop table `nz_test_closure`.`new_field`;
	truncate table `nz_test_closure`.`field_add_list`;
end //

#--------------------------------------------------
/*
delimiter //
drop procedure if exists get_all_ancestors//
create procedure get_all_ancestors(id INT)
begin
	select t.name
	from `nz_test_closure`.`type` as t
	join `nz_test_closure`.`type_closure` as clos
	on t.id = clos.ancestor
	where clos.descendant = id
		and t.id != id;	# filter itself
end //

delimiter //
drop procedure if exists get_all_descendants//
create procedure get_all_descendants(id INT)
begin
	select t.name
	from `nz_test_closure`.`type` as t
	join `nz_test_closure`.`type_closure` as clos
	on t.id = clos.descendant
	where clos.ancestor = id
		and t.id != id;	# filter itself
end //

delimiter ;
*/