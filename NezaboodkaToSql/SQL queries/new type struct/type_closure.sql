create database if not exists nz_test_closure;

use nz_test_closure;

create table `type`(
	`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
	`name` VARCHAR(128) not null unique check(`name` != ''),
    `table_name` varchar(64) not null unique check(`table_name` != ''),
	`base_type_name` VARCHAR(128) collate `utf8_general_ci`
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;

create table `type_closure`(
	`ancestor` INT NOT NULL,
	`descendant` INT NOT NULL,
    `is_straight` BOOLEAN NOT NULL DEFAULT false,
    
    foreign key(`ancestor`)
		references `type`(`id`)
		on delete cascade,
    foreign key(`descendant`)
		references `type`(`id`)
		on delete cascade,
	constraint `uc_keys` unique (`ancestor`, `descendant`)
);

CREATE TEMPORARY TABLE IF NOT EXISTS `type_add_list`(
	`name` VARCHAR(128) NOT NULL UNIQUE,
	`table_name` VARCHAR(64) NOT NULL UNIQUE,
	`base_type_name` VARCHAR(128)
) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;

truncate table `type_add_list`;


delimiter //
drop procedure if exists add_all_types//
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
    
    #drop table `new_type`;
end //

delimiter //
drop procedure if exists add_type//
create procedure add_type(type_name VARCHAR(128))
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
drop procedure if exists remove_type //
create procedure remove_type(type_name varchar(128))
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
